import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import {
  Table,
  TableHeader,
  TableBody,
  TableHead,
  TableRow,
  TableCell,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { formatCurrency } from '@/lib/format';
import type { Deal } from '@/lib/mockData';

interface RecentDealsProps {
  deals: Deal[];
}

const STAGE_VARIANT: Record<Deal['stage'], 'default' | 'secondary' | 'outline' | 'destructive'> = {
  'Closed Won': 'default',
  'Negotiation': 'secondary',
  'Proposal Sent': 'outline',
  'Discovery': 'outline',
};

const STAGE_DOT: Record<Deal['stage'], string> = {
  'Closed Won': 'bg-success',
  'Negotiation': 'bg-chart-2',
  'Proposal Sent': 'bg-chart-3',
  'Discovery': 'bg-muted-foreground',
};

export function RecentDeals({ deals }: RecentDealsProps) {
  return (
    <Card className="shadow-sm">
      <CardHeader className="pb-4">
        <CardTitle className="text-base font-semibold">Recent Deals</CardTitle>
        <CardDescription>Latest opportunities across the pipeline</CardDescription>
      </CardHeader>
      <CardContent className="px-0 pb-0">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <TableHead className="pl-6">Company</TableHead>
              <TableHead>Rep</TableHead>
              <TableHead>Stage</TableHead>
              <TableHead className="pr-6 text-right">Value</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {deals.map((deal) => (
              <TableRow key={deal.company} className="hover:bg-muted/40">
                <TableCell className="pl-6 py-3">
                  <div className="flex items-center gap-2.5">
                    <Avatar className="h-8 w-8 border">
                      <AvatarFallback className="bg-muted text-[10px] font-bold text-muted-foreground">
                        {deal.logo}
                      </AvatarFallback>
                    </Avatar>
                    <div>
                      <p className="text-sm font-medium">{deal.company}</p>
                      <p className="text-xs text-muted-foreground">{deal.closeDate}</p>
                    </div>
                  </div>
                </TableCell>
                <TableCell className="py-3 text-sm text-muted-foreground">{deal.rep}</TableCell>
                <TableCell className="py-3">
                  <Badge variant={STAGE_VARIANT[deal.stage]} className="gap-1.5">
                    <span className={`h-1.5 w-1.5 rounded-full ${STAGE_DOT[deal.stage]}`} />
                    {deal.stage}
                  </Badge>
                </TableCell>
                <TableCell className="pr-6 py-3 text-right text-sm font-semibold">
                  {formatCurrency(deal.value)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}
