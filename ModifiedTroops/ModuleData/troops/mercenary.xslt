<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='eastern_mercenary']"/>
    <xsl:template match="NPCCharacter[@id='eastern_mercenary_t4']"/>
    <xsl:template match="NPCCharacter[@id='eastern_mercenary_t5']"/>

</xsl:stylesheet>
